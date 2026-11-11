from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.compression_preview_info import CompressionPreviewInfo
from ...models.compression_preview_request import CompressionPreviewRequest
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    *,
    body: CompressionPreviewRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/export/compression",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> CompressionPreviewInfo | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = CompressionPreviewInfo.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if response.status_code == 422:
        response_422 = ProblemDetails.from_dict(response.json())



        return response_422

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[CompressionPreviewInfo | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: CompressionPreviewRequest,

) -> Response[CompressionPreviewInfo | ProblemDetails]:
    """ A region of one exported file as the file holds it, beside its source, with the error per lane.

    Args:
        body (CompressionPreviewRequest): The body of `POST /api/v1/export/compression` (#217):
            one file of an export, to see a region of it as the
            file holds it beside what it was made from.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[CompressionPreviewInfo | ProblemDetails]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: CompressionPreviewRequest,

) -> CompressionPreviewInfo | ProblemDetails | None:
    """ A region of one exported file as the file holds it, beside its source, with the error per lane.

    Args:
        body (CompressionPreviewRequest): The body of `POST /api/v1/export/compression` (#217):
            one file of an export, to see a region of it as the
            file holds it beside what it was made from.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        CompressionPreviewInfo | ProblemDetails
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: CompressionPreviewRequest,

) -> Response[CompressionPreviewInfo | ProblemDetails]:
    """ A region of one exported file as the file holds it, beside its source, with the error per lane.

    Args:
        body (CompressionPreviewRequest): The body of `POST /api/v1/export/compression` (#217):
            one file of an export, to see a region of it as the
            file holds it beside what it was made from.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[CompressionPreviewInfo | ProblemDetails]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: CompressionPreviewRequest,

) -> CompressionPreviewInfo | ProblemDetails | None:
    """ A region of one exported file as the file holds it, beside its source, with the error per lane.

    Args:
        body (CompressionPreviewRequest): The body of `POST /api/v1/export/compression` (#217):
            one file of an export, to see a region of it as the
            file holds it beside what it was made from.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        CompressionPreviewInfo | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
